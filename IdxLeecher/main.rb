require 'net/http'
require 'uri'
require 'CGI'

class Main

  def run_all_letters

    words = []

    97.upto(122) { |chr_idx|
      char = chr_idx.chr.upcase

      puts "Fetching #{char}..."
      char_uri = URI.parse("http://education.yahoo.com/reference/dictionary/entry_index?letter=#{char}")

      char_web_page = Net::HTTP.get(char_uri)
      char_web_page = CGI::unescapeHTML char_web_page

      matches = char_web_page.scan(/entry_index\?letter=#{Regexp.quote(char)}\&key=(\S+)\"/)
      matches.each { |capture|
        puts "Fetching #{char} #{CGI::unescape capture[0]}..."

        entries_uri = "http://education.yahoo.com/reference/dictionary/entry_index?letter=#{char}&key=#{capture[0]}"
        entries_web_page = Net::HTTP.get(URI.parse(entries_uri))
        entries_web_page = CGI::unescapeHTML entries_web_page

        # We need the text inside the anchor: <a href="/reference/dictionary/entry/Abigail">Abigail</a>
        entries_matches = entries_web_page.scan(/entry\/\S+\">(\S+)<\/a>/)

        entries_matches.each { |entry| words.push entry[0] }
      }
    }

    words.uniq!
    words.sort!

    File.open("words2.txt", "w") { |the_file|
      words.each { |word| the_file.puts word }
    }

  end

end

Main.new().run_all_letters()